using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Servicio para manejo unificado del botÃ³n de bÃºsqueda
    /// </summary>
    public partial class MainForm
    {
        // Estados del botÃ³n unificado
        private enum ButtonState
        {
            Idle,           // Inactivo - listo para iniciar
            Starting,       // Iniciando - preparando bÃºsqueda
            Running,        // Ejecutando - bÃºsqueda en progreso
            Stopping,       // Deteniendo - cancelando bÃºsqueda
            Completed       // Completado - bÃºsqueda finalizada
        }
        
        private ButtonState currentButtonState = ButtonState.Idle;
        
        /// <summary>
        /// Actualizar estado del botÃ³n unificado
        /// </summary>
        private void UpdateButtonState(ButtonState newState)
        {
            try
            {
                currentButtonState = newState;
                
                if (startAuthorSearchButton.InvokeRequired)
                {
                    startAuthorSearchButton.Invoke(new Action<ButtonState>(UpdateButtonState), newState);
                    return;
                }
                
                switch (newState)
                {
                    case ButtonState.Idle:
                        SetButtonIdle();
                        break;
                    case ButtonState.Starting:
                        SetButtonStarting();
                        break;
                    case ButtonState.Running:
                        SetButtonRunning();
                        break;
                    case ButtonState.Stopping:
                        SetButtonStopping();
                        break;
                    case ButtonState.Completed:
                        SetButtonCompleted();
                        break;
                }
                
                Console.WriteLine($"[UnifiedButton] ðŸ”„ Estado actualizado: {newState}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UnifiedButton] âŒ Error actualizando estado: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Configurar botÃ³n en estado inactivo
        /// </summary>
        private void SetButtonIdle()
        {
            startAuthorSearchButton.Text = "ðŸš€ Iniciar BÃºsqueda Ultra-RÃ¡pida";
            startAuthorSearchButton.BackColor = Color.FromArgb(0, 120, 215);
            startAuthorSearchButton.ForeColor = Color.White;
            startAuthorSearchButton.Enabled = true;
            startAuthorSearchButton.Cursor = Cursors.Hand;
        }
        
        /// <summary>
        /// Configurar botÃ³n en estado iniciando
        /// </summary>
        private void SetButtonStarting()
        {
            startAuthorSearchButton.Text = "â³ Iniciando...";
            startAuthorSearchButton.BackColor = Color.FromArgb(255, 165, 0); // Naranja
            startAuthorSearchButton.ForeColor = Color.White;
            startAuthorSearchButton.Enabled = false;
            startAuthorSearchButton.Cursor = Cursors.WaitCursor;
        }
        
        /// <summary>
        /// Configurar botÃ³n en estado ejecutando
        /// </summary>
        private void SetButtonRunning()
        {
            startAuthorSearchButton.Text = "â¹ï¸ Detener BÃºsqueda";
            startAuthorSearchButton.BackColor = Color.FromArgb(220, 53, 69); // Rojo
            startAuthorSearchButton.ForeColor = Color.White;
            startAuthorSearchButton.Enabled = true;
            startAuthorSearchButton.Cursor = Cursors.Hand;
        }
        
        /// <summary>
        /// Configurar botÃ³n en estado deteniendo
        /// </summary>
        private void SetButtonStopping()
        {
            startAuthorSearchButton.Text = "â¹ï¸ Deteniendo...";
            startAuthorSearchButton.BackColor = Color.FromArgb(255, 140, 0); // Naranja oscuro
            startAuthorSearchButton.ForeColor = Color.White;
            startAuthorSearchButton.Enabled = false;
            startAuthorSearchButton.Cursor = Cursors.WaitCursor;
        }
        
        /// <summary>
        /// Configurar botÃ³n en estado completado
        /// </summary>
        private void SetButtonCompleted()
        {
            startAuthorSearchButton.Text = "âœ… BÃºsqueda Completada";
            startAuthorSearchButton.BackColor = Color.FromArgb(40, 167, 69); // Verde
            startAuthorSearchButton.ForeColor = Color.White;
            startAuthorSearchButton.Enabled = true;
            startAuthorSearchButton.Cursor = Cursors.Hand;
            
            // Restaurar a estado inactivo despuÃ©s de 3 segundos
            var timer = new Timer { Interval = 3000 };
            timer.Tick += (s, e) =>
            {
                UpdateButtonState(ButtonState.Idle);
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }
        
        /// <summary>
        /// Manejar clic del botÃ³n unificado
        /// </summary>
        private async Task HandleUnifiedButtonClick()
        {
            try
            {
                // Evitar clics mÃºltiples
                if (currentButtonState == ButtonState.Starting || currentButtonState == ButtonState.Stopping)
                {
                    return;
                }
                
                switch (currentButtonState)
                {
                    case ButtonState.Idle:
                        await StartSearchProcess();
                        break;
                    case ButtonState.Running:
                        await StopSearchProcess();
                        break;
                    case ButtonState.Completed:
                        // Reiniciar para nueva bÃºsqueda
                        UpdateButtonState(ButtonState.Idle);
                        break;
                    default:
                        // Ignorar clic en otros estados
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UnifiedButton] âŒ Error manejando clic: {ex.Message}");
                UpdateButtonState(ButtonState.Idle);
            }
        }
        
        /// <summary>
        /// Iniciar proceso de bÃºsqueda
        /// </summary>
        private async Task StartSearchProcess()
        {
            try
            {
                if (authorsListBox.SelectedItems.Count == 0)
                {
                    DarkMessageBox.Show("Selecciona al menos un autor de la lista", "Auto-BÃºsqueda", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Verificar conexiÃ³n
                if (client?.State != SoulseekClientStates.Connected && client?.State != SoulseekClientStates.LoggedIn)
                {
                    DarkMessageBox.Show("No estÃ¡s conectado a Soulseek. Conecta primero e intenta de nuevo.", "Sin ConexiÃ³n", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                UpdateButtonState(ButtonState.Starting);
                
                // Convertir selecciÃ³n en background para no bloquear UI
                var selectedAuthors = await Task.Run(() => 
                    authorsListBox.SelectedItems.Cast<string>().ToList()
                );
                
                isAuthorSearchRunning = true;
                UpdateButtonState(ButtonState.Running);
                
                // Iniciar bÃºsqueda ultra-rÃ¡pida
                await StartUltraFastSearchAsync(selectedAuthors);
                
                // La bÃºsqueda se completarÃ¡ y actualizarÃ¡ el estado automÃ¡ticamente
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UnifiedButton] âŒ Error iniciando bÃºsqueda: {ex.Message}");
                UpdateButtonState(ButtonState.Idle);
            }
        }
        
        /// <summary>
        /// Detener proceso de bÃºsqueda
        /// </summary>
        private async Task StopSearchProcess()
        {
            try
            {
                UpdateButtonState(ButtonState.Stopping);
                
                isAuthorSearchRunning = false;
                
                // Mostrar mensaje en log
                AddColoredLogMessage("\r\nâ¹ï¸ BÃšSQUEDA DETENIDA POR EL USUARIO\r\n", LogMessageType.Warning);
                
                // Enviar notificaciÃ³n
                ShowWindowsNotification("ðŸ›‘ BÃºsqueda Detenida", "La bÃºsqueda automÃ¡tica ha sido detenida por el usuario", WindowsNotificationService.NotificationType.Warning);
                
                // PequeÃ±a pausa para mostrar estado de detenciÃ³n
                await Task.Delay(1000);
                
                UpdateButtonState(ButtonState.Idle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UnifiedButton] âŒ Error deteniendo bÃºsqueda: {ex.Message}");
                UpdateButtonState(ButtonState.Idle);
            }
        }
        
        /// <summary>
        /// Notificar completado de bÃºsqueda
        /// </summary>
        private void NotifySearchCompleted(int authorsProcessed, int filesFound, TimeSpan elapsedTime)
        {
            try
            {
                UpdateButtonState(ButtonState.Completed);
                
                // Enviar notificaciÃ³n
                NotifySearchCompleted(authorsProcessed, filesFound, elapsedTime);
                
                Console.WriteLine($"[UnifiedButton] âœ… BÃºsqueda completada: {authorsProcessed} autores, {filesFound} archivos");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UnifiedButton] âŒ Error notificando completado: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mostrar informaciÃ³n del botÃ³n unificado
        /// </summary>
        private void ShowUnifiedButtonInfo()
        {
            try
            {
                var info = $"""
ðŸ”„ BOTÃ“N UNIFICADO DE BÃšSQUEDA
========================================
ðŸŽ¯ Estados del BotÃ³n:
â”œâ”€â”€ ðŸš€ Inactivo: Listo para iniciar bÃºsqueda
â”œâ”€â”€ â³ Iniciando: Preparando bÃºsqueda ultra-rÃ¡pida
â”œâ”€â”€ â¹ï¸ Ejecutando: BÃºsqueda en progreso (clic para detener)
â”œâ”€â”€ â¹ï¸ Deteniendo: Cancelando bÃºsqueda actual
â””â”€â”€ âœ… Completado: BÃºsqueda finalizada (auto-restaura)

ðŸŽ¨ Colores por Estado:
â”œâ”€â”€ ðŸ”µ Azul: Inactivo (listo para usar)
â”œâ”€â”€ ðŸŸ  Naranja: Procesando (inicio/detenciÃ³n)
â”œâ”€â”€ ðŸ”´ Rojo: Ejecutando (clic para detener)
â””â”€â”€ ðŸŸ¢ Verde: Completado (Ã©xito)

ðŸ’¡ Comportamiento Inteligente:
â”œâ”€â”€ âœ… Un botÃ³n para iniciar/detener
â”œâ”€â”€ ðŸ”„ Estados visuales claros
â”œâ”€â”€ ðŸš« Deshabilitado durante operaciones
â”œâ”€â”€ ðŸ“¡ Notificaciones automÃ¡ticas
â”œâ”€â”€ ðŸŽ¯ Cursor adaptativo por estado
â””â”€â”€ â° Auto-restauraciÃ³n tras completar

ðŸŽ® Uso Simplificado:
â”œâ”€â”€ 1 clic: Iniciar bÃºsqueda
â”œâ”€â”€ 1 clic: Detener bÃºsqueda
â”œâ”€â”€ 1 clic: Reiniciar tras completar
â””â”€â”€ 0 confusiones: Siempre claro quÃ© hace

Estado Actual: {currentButtonState}
""";
                
                Console.WriteLine(info);
                MessageBox.Show(info, "BotÃ³n Unificado - SlskDown", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UnifiedButton] âŒ Error mostrando informaciÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Inicializar servicio de botÃ³n unificado
        /// </summary>
        private void InitializeUnifiedButton()
        {
            try
            {
                UpdateButtonState(ButtonState.Idle);
                Console.WriteLine("[UnifiedButton] âœ… BotÃ³n unificado inicializado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UnifiedButton] âŒ Error inicializando: {ex.Message}");
            }
        }
    }
}

