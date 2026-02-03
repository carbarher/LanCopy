using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Panel de log ultra-rÃ¡pido con diseÃ±o moderno y mÃ©tricas en tiempo real
    /// </summary>
    public partial class MainForm
    {
        // Controles del nuevo panel de log
        private Panel ultraFastLogPanel = null!;
        private RichTextBox mainLogBox = null!;
        private Panel statsPanel = null!;
        private Label speedLabel = null!;
        private Label progressLabel = null!;
        private Label cacheLabel = null!;
        private Label timeLabel = null!;
        private ProgressBar overallProgressBar = null!;
        private Label phaseLabel = null!;
        
        /// <summary>
        /// Crear panel de log ultra-rÃ¡pido
        /// </summary>
        private void CreateUltraFastLogPanel()
        {
            try
            {
                Console.WriteLine("[UltraFastLog] ðŸŽ¨ Creando panel de log ultra-rÃ¡pido");
                
                // Panel principal
                ultraFastLogPanel = new Panel();
                ultraFastLogPanel.Location = new Point(220, 115);
                ultraFastLogPanel.Size = new Size(1000, 450);
                ultraFastLogPanel.BackColor = Color.FromArgb(15, 15, 15);
                ultraFastLogPanel.BorderStyle = BorderStyle.FixedSingle;
                ultraFastLogPanel.BringToFront();
                
                // Panel de estadÃ­sticas (superior)
                CreateStatsPanel();
                
                // Panel de fase actual
                CreatePhasePanel();
                
                // Caja de log principal
                CreateMainLogBox();
                
                // Barra de progreso general
                CreateOverallProgressBar();
                
                Console.WriteLine("[UltraFastLog] âœ… Panel de log ultra-rÃ¡pido creado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFastLog] âŒ Error creando panel: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Crear panel de estadÃ­sticas en tiempo real
        /// </summary>
        private void CreateStatsPanel()
        {
            statsPanel = new Panel();
            statsPanel.Location = new Point(0, 0);
            statsPanel.Size = new Size(1000, 80);
            statsPanel.BackColor = Color.FromArgb(25, 25, 35);
            statsPanel.BorderStyle = BorderStyle.None;
            
            // TÃ­tulo del panel
            var titleLabel = new Label();
            titleLabel.Text = "ðŸš€ MODO ULTRA-RÃPIDO - MÃ‰TRICAS EN TIEMPO REAL";
            titleLabel.Location = new Point(10, 5);
            titleLabel.Size = new Size(500, 20);
            titleLabel.ForeColor = Color.FromArgb(59, 130, 246);
            titleLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            statsPanel.Controls.Add(titleLabel);
            
            // MÃ©tricas principales
            speedLabel = new Label();
            speedLabel.Text = "âš¡ Velocidad: 0.0 autores/min";
            speedLabel.Location = new Point(10, 30);
            speedLabel.Size = new Size(200, 20);
            speedLabel.ForeColor = Color.LightGreen;
            speedLabel.Font = new Font("Consolas", 9);
            statsPanel.Controls.Add(speedLabel);
            
            progressLabel = new Label();
            progressLabel.Text = "ðŸ“Š Progreso: 0/0 (0%)";
            progressLabel.Location = new Point(220, 30);
            progressLabel.Size = new Size(200, 20);
            progressLabel.ForeColor = Color.LightBlue;
            progressLabel.Font = new Font("Consolas", 9);
            statsPanel.Controls.Add(progressLabel);
            
            cacheLabel = new Label();
            cacheLabel.Text = "ðŸŽ¯ Cache: 0 hits (0%)";
            cacheLabel.Location = new Point(430, 30);
            cacheLabel.Size = new Size(180, 20);
            cacheLabel.ForeColor = Color.Yellow;
            cacheLabel.Font = new Font("Consolas", 9);
            statsPanel.Controls.Add(cacheLabel);
            
            timeLabel = new Label();
            timeLabel.Text = "â±ï¸ Tiempo: 00:00:00";
            timeLabel.Location = new Point(630, 30);
            timeLabel.Size = new Size(150, 20);
            timeLabel.ForeColor = Color.Orange;
            timeLabel.Font = new Font("Consolas", 9);
            statsPanel.Controls.Add(timeLabel);
            
            // Estado de optimizaciones
            var optimizationsLabel = new Label();
            optimizationsLabel.Text = "ðŸ”§ Optimizaciones: âœ…Cache âœ…Lotes âœ…Paralelo âœ…Timeout";
            optimizationsLabel.Location = new Point(10, 55);
            optimizationsLabel.Size = new Size(600, 20);
            optimizationsLabel.ForeColor = Color.Gray;
            optimizationsLabel.Font = new Font("Segoe UI", 8);
            statsPanel.Controls.Add(optimizationsLabel);
            
            ultraFastLogPanel.Controls.Add(statsPanel);
        }
        
        /// <summary>
        /// Crear panel de fase actual
        /// </summary>
        private void CreatePhasePanel()
        {
            var phasePanel = new Panel();
            phasePanel.Location = new Point(0, 80);
            phasePanel.Size = new Size(1000, 30);
            phasePanel.BackColor = Color.FromArgb(35, 35, 45);
            
            phaseLabel = new Label();
            phaseLabel.Text = "ðŸ”„ FASE 1/4: OptimizaciÃ³n con Cache";
            phaseLabel.Location = new Point(10, 5);
            phaseLabel.Size = new Size(400, 20);
            phaseLabel.ForeColor = Color.White;
            phaseLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            phasePanel.Controls.Add(phaseLabel);
            
            ultraFastLogPanel.Controls.Add(phasePanel);
        }
        
        /// <summary>
        /// Crear caja de log principal mejorada
        /// </summary>
        private void CreateMainLogBox()
        {
            mainLogBox = new RichTextBox();
            mainLogBox.Dock = DockStyle.Fill;
            mainLogBox.BackColor = Color.FromArgb(20, 20, 20);
            mainLogBox.ForeColor = Color.LimeGreen;
            mainLogBox.Font = new Font("Consolas", 10);
            mainLogBox.Multiline = true;
            mainLogBox.ReadOnly = true;
            mainLogBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            mainLogBox.WordWrap = false;
            mainLogBox.BorderStyle = BorderStyle.FixedSingle;
            
            // ConfiguraciÃ³n anti-parpadeo mejorada
            mainLogBox.DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            
            // Texto inicial
            mainLogBox.AppendText("ðŸš€ SISTEMA ULTRA-RÃPIDO LISTO\r\n");
            mainLogBox.AppendText("âš¡ Optimizaciones: Cache + Lotes + Paralelo + Timeout Adaptativo\r\n");
            mainLogBox.AppendText("ðŸ“Š Esperando inicio de bÃºsqueda...\r\n");
            mainLogBox.AppendText("â”€".PadRight(80, 'â”€') + "\r\n");
            
            ultraFastLogPanel.Controls.Add(mainLogBox);
        }
        
        /// <summary>
        /// Crear barra de progreso general
        /// </summary>
        private void CreateOverallProgressBar()
        {
            overallProgressBar = new ProgressBar();
            overallProgressBar.Location = new Point(0, 410);
            overallProgressBar.Size = new Size(1000, 25);
            overallProgressBar.Style = ProgressBarStyle.Continuous;
            overallProgressBar.BackColor = Color.FromArgb(40, 40, 40);
            overallProgressBar.ForeColor = Color.FromArgb(59, 130, 246);
            overallProgressBar.Value = 0;
            
            ultraFastLogPanel.Controls.Add(overallProgressBar);
        }
        
        /// <summary>
        /// Actualizar estadÃ­sticas en tiempo real
        /// </summary>
        private void UpdateRealTimeStats(int processedAuthors, int totalAuthors, int cacheHits, TimeSpan elapsedTime)
        {
            try
            {
                if (ultraFastLogPanel.InvokeRequired)
                {
                    ultraFastLogPanel.Invoke(new Action<int, int, int, TimeSpan>(UpdateRealTimeStats), processedAuthors, totalAuthors, cacheHits, elapsedTime);
                    return;
                }
                
                // Calcular mÃ©tricas
                var progressPercent = totalAuthors > 0 ? (processedAuthors * 100) / totalAuthors : 0;
                var speed = elapsedTime.TotalMinutes > 0 ? processedAuthors / elapsedTime.TotalMinutes : 0;
                var cacheHitRate = processedAuthors > 0 ? (cacheHits * 100) / processedAuthors : 0;
                
                // Actualizar labels
                speedLabel.Text = $"âš¡ Velocidad: {speed:F1} autores/min";
                progressLabel.Text = $"ðŸ“Š Progreso: {processedAuthors}/{totalAuthors} ({progressPercent}%)";
                cacheLabel.Text = $"ðŸŽ¯ Cache: {cacheHits} hits ({cacheHitRate}%)";
                timeLabel.Text = $"â±ï¸ Tiempo: {elapsedTime:hh\\:mm\\:ss}";
                
                // Actualizar barra de progreso
                overallProgressBar.Value = Math.Min(100, progressPercent);
                
                // Cambiar color segÃºn velocidad
                if (speed > 15)
                {
                    speedLabel.ForeColor = Color.LimeGreen; // Muy rÃ¡pido
                }
                else if (speed > 8)
                {
                    speedLabel.ForeColor = Color.Yellow; // RÃ¡pido
                }
                else
                {
                    speedLabel.ForeColor = Color.Orange; // Normal
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFastLog] âŒ Error actualizando stats: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Actualizar fase actual
        /// </summary>
        private void UpdateCurrentPhase(int phaseNumber, string phaseDescription)
        {
            try
            {
                if (phaseLabel.InvokeRequired)
                {
                    phaseLabel.Invoke(new Action<int, string>(UpdateCurrentPhase), phaseNumber, phaseDescription);
                    return;
                }
                
                phaseLabel.Text = $"ðŸ”„ FASE {phaseNumber}/4: {phaseDescription}";
                
                // Cambiar color segÃºn fase
                switch (phaseNumber)
                {
                    case 1:
                        phaseLabel.ForeColor = Color.LightBlue; // Cache
                        break;
                    case 2:
                        phaseLabel.ForeColor = Color.LightGreen; // Lotes
                        break;
                    case 3:
                        phaseLabel.ForeColor = Color.Yellow; // Paralelo
                        break;
                    case 4:
                        phaseLabel.ForeColor = Color.Lime; // Final
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFastLog] âŒ Error actualizando fase: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Agregar mensaje coloreado al log
        /// </summary>
        private void AddColoredLogMessage(string message, LogMessageType type = LogMessageType.Info)
        {
            try
            {
                Console.WriteLine($"[UltraFastLog] ðŸ“ {message}");
                
                if (mainLogBox.InvokeRequired)
                {
                    mainLogBox.Invoke(new Action<string, LogMessageType>(AddColoredLogMessage), message, type);
                    return;
                }
                
                var originalColor = mainLogBox.SelectionColor;
                var originalFont = mainLogBox.SelectionFont;
                
                // Configurar color segÃºn tipo
                    switch (type)
                {
                    case LogMessageType.Success:
                        mainLogBox.SelectionColor = Color.LimeGreen;
                        break;
                    case LogMessageType.Warning:
                        mainLogBox.SelectionColor = Color.Yellow;
                        break;
                    case LogMessageType.Error:
                        mainLogBox.SelectionColor = Color.Red;
                        break;
                    case LogMessageType.Cache:
                        mainLogBox.SelectionColor = Color.Cyan;
                        break;
                    case LogMessageType.Speed:
                        mainLogBox.SelectionColor = Color.LightGreen;
                        break;
                    case LogMessageType.Phase:
                        mainLogBox.SelectionColor = Color.White;
                        mainLogBox.SelectionFont = new Font("Consolas", 9, FontStyle.Bold);
                        break;
                    default:
                        mainLogBox.SelectionColor = Color.LightGray;
                        break;
                }
                
                // Agregar mensaje
                mainLogBox.AppendText(message + "\r\n");
                
                // Restaurar formato original
                mainLogBox.SelectionColor = originalColor;
                mainLogBox.SelectionFont = originalFont;
                
                // Auto-scroll al final
                mainLogBox.SelectionStart = mainLogBox.Text.Length;
                mainLogBox.ScrollToCaret();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFastLog] âŒ Error agregando mensaje: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Limpiar log para nueva bÃºsqueda
        /// </summary>
        private void ClearUltraFastLog()
        {
            try
            {
                if (mainLogBox.InvokeRequired)
                {
                    mainLogBox.Invoke(new Action(ClearUltraFastLog));
                    return;
                }
                
                mainLogBox.Clear();
                mainLogBox.AppendText("ðŸš€ INICIANDO BÃšSQUEDA ULTRA-RÃPIDA\r\n");
                mainLogBox.AppendText("â”€".PadRight(80, 'â”€') + "\r\n");
                
                // Resetear estadÃ­sticas
                speedLabel.Text = "âš¡ Velocidad: 0.0 autores/min";
                progressLabel.Text = "ðŸ“Š Progreso: 0/0 (0%)";
                cacheLabel.Text = "ðŸŽ¯ Cache: 0 hits (0%)";
                timeLabel.Text = "â±ï¸ Tiempo: 00:00:00";
                overallProgressBar.Value = 0;
                
                Console.WriteLine("[UltraFastLog] ðŸ§¹ Log limpiado para nueva bÃºsqueda");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFastLog] âŒ Error limpiando log: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tipos de mensaje para el log coloreado
        /// </summary>
        public enum LogMessageType
        {
            Info,
            Success,
            Warning,
            Error,
            Cache,
            Speed,
            Phase
        }
        
        /// <summary>
        /// Reemplazar el log antiguo con el nuevo diseÃ±o
        /// </summary>
        private void ReplaceOldLogWithUltraFast()
        {
            try
            {
                // Remover log antiguo si existe
                if (authorSearchLog != null && this.Controls.Contains(authorSearchLog))
                {
                    this.Controls.Remove(authorSearchLog);
                    authorSearchLog.Dispose();
                }
                
                // Crear y agregar nuevo panel
                CreateUltraFastLogPanel();
                this.Controls.Add(ultraFastLogPanel);
                
                Console.WriteLine("[UltraFastLog] âœ… Log antiguo reemplazado por diseÃ±o ultra-rÃ¡pido");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFastLog] âŒ Error reemplazando log: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mostrar resumen final en el log
        /// </summary>
        private void ShowFinalSummary(int totalAuthors, int totalFiles, int cacheHits, TimeSpan totalTime)
        {
            try
            {
                AddColoredLogMessage("", LogMessageType.Phase);
                AddColoredLogMessage("ðŸ† RESUMEN FINAL - MODO ULTRA-RÃPIDO", LogMessageType.Phase);
                AddColoredLogMessage("â•".PadRight(80, 'â•'), LogMessageType.Phase);
                
                var speed = totalTime.TotalMinutes > 0 ? totalAuthors / totalTime.TotalMinutes : 0;
                var efficiency = totalAuthors > 0 ? (cacheHits * 100) / totalAuthors : 0;
                
                AddColoredLogMessage($"ðŸ“Š Autores procesados: {totalAuthors}", LogMessageType.Success);
                AddColoredLogMessage($"ðŸ“ Archivos encontrados: {totalFiles:N0}", LogMessageType.Success);
                AddColoredLogMessage($"ðŸŽ¯ Cache hits: {cacheHits} ({efficiency}%)", LogMessageType.Cache);
                AddColoredLogMessage($"âš¡ Velocidad promedio: {speed:F1} autores/minuto", LogMessageType.Speed);
                AddColoredLogMessage($"â±ï¸ Tiempo total: {totalTime:hh\\:mm\\:ss}", LogMessageType.Info);
                
                AddColoredLogMessage("", LogMessageType.Info);
                AddColoredLogMessage("ðŸš€ MODO ULTRA-RÃPIDO COMPLETADO CON Ã‰XITO", LogMessageType.Success);
                AddColoredLogMessage("â•".PadRight(80, 'â•'), LogMessageType.Phase);
                
                Console.WriteLine("[UltraFastLog] ðŸ“Š Resumen final mostrado en el log");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UltraFastLog] âŒ Error mostrando resumen: {ex.Message}");
            }
        }
    }
}

